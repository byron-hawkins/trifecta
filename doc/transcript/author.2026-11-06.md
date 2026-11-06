*Author: Byron*

*Subject: identify cases in both the customer experience and system behavioral capability*


## Additional Design Factors

### Future Proof

1. Sysbem Distribution
    1. role: partition software components to allow for deployment across multiple devices, including:
        - sharing sensors among a group of manufacturing machines
        - centralizing control of multiple machines
        - sharing stages and/or stage tasks among multiple machines
2. Error Handling
    1. role: establish awareness and a reaction model for error events in a resource
3. Health Check
    1. role: define a generic telemetry interface and equip components with reporting capabilites as need arises
4. Logging
    1. role: formulate a binary encoding of software events while designing components to maintain event awareness
5. Testing
    1. role: factorize component interfacing to maximize effectiveness of mocks and scripted event sequences
6. Symbolic Execution
    1. role: maximize visibility of high-risk data structures and code paths using machine-parseable annotations
7. Dynamic Tasks
    1. role: traverse a stage task sequence actively over an interface query to allow realtime adaptations

